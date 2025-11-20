<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:include href="_utils.xsl"/>

	<xsl:param name="sitetype"/>
	<xsl:param name="project-id"/>
	<xsl:param name="editor-id"/>
	<xsl:param name="reporttype-id"/>
	<xsl:param name="ocvariantid"/>
	<xsl:param name="permissions"/>

	<xsl:variable name="nonbreaking_space">&#160;</xsl:variable>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>



	<xsl:template match="/">
		<!--		<div>FROM XSLT</div>
		<div> editor id: <xsl:value-of select="$editor-id"/>
			<br/> reporttype id: <xsl:value-of select="$reporttype-id"/>
			<p>ocvariantid: <xsl:value-of select="$ocvariantid"/>, containsoutputchannel: <xsl:value-of select="$containsoutputchannel"/></p>
		</div>

-->
		<xsl:apply-templates select="/configuration/editors/editor[@id = $editor-id]/output_channels/output_channel[variants/variant]"/>
	</xsl:template>

	<xsl:template match="output_channel">
		<xsl:variable name="type" select="@type"/>
		<xsl:variable name="containsoutputchannel">
			<xsl:choose>
				<xsl:when test="variants/variant[@id = $ocvariantid]">yes</xsl:when>
				<xsl:otherwise>no</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<div class="outputchannelwrapper panel-export{$type}">

			<h3 class="panel-title">Render <xsl:value-of select="name"/> documents and variants</h3>

			<div class="outputchanneltabswrapper">

				<!-- Nav tabs -->
				<ul class="nav nav-tabs" role="tablist">
					<xsl:for-each select="variants/variant">
						<li role="presentation">
							<xsl:choose>
								<xsl:when test="$containsoutputchannel = 'yes' and @id = $ocvariantid">
									<xsl:attribute name="class">active</xsl:attribute>
								</xsl:when>
								<xsl:when test="$containsoutputchannel = 'no' and position() = 1">
									<xsl:attribute name="class">active</xsl:attribute>
								</xsl:when>
								<xsl:otherwise/>
							</xsl:choose>

							<a href="#{@id}" aria-controls="{@id}" role="tab" data-toggle="tab" data-octype="{$type}" data-oclang="{@lang}" data-ocvariantid="{@id}">
								<xsl:value-of select="name"/>
							</a>
						</li>
					</xsl:for-each>
				</ul>

				<!-- Tab panes -->
				<div class="tab-content publication-variants">
					<xsl:apply-templates select="variants/variant">
						<xsl:with-param name="outputchanneltype" select="$type"/>
						<xsl:with-param name="containsoutputchannel" select="$containsoutputchannel"/>
					</xsl:apply-templates>
				</div>

			</div>


		</div>
	</xsl:template>

	<xsl:template match="variant">
		<xsl:param name="outputchanneltype"/>
		<xsl:param name="containsoutputchannel"/>

		<xsl:variable name="variantid" select="@id"/>
		<xsl:variable name="variantlanguage" select="@lang"/>
		<xsl:variable name="metadataidref" select="@metadata-id-ref"/>
		<xsl:variable name="renderlayoutoptions">
			<xsl:choose>
				<xsl:when test="@forcedlayout='true'">no</xsl:when>
                <xsl:when test="count(../../layouts/layout[not(@disabled)]) &gt; 1">yes</xsl:when>
				<xsl:otherwise>no</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<div role="tabpanel" class="tab-pane" id="{@id}">
			<xsl:choose>
				<xsl:when test="$containsoutputchannel = 'yes' and @id = $ocvariantid">
					<xsl:attribute name="class">tab-pane active</xsl:attribute>
				</xsl:when>
				<xsl:when test="$containsoutputchannel = 'no' and position() = 1">
					<xsl:attribute name="class">tab-pane active</xsl:attribute>
				</xsl:when>
				<xsl:otherwise/>
			</xsl:choose>
			<!--<xsl:if test="@id=$ocvariantid">
				<xsl:attribute name="class">tab-pane active</xsl:attribute>
			</xsl:if>-->



			<div class="publicationvariantwrapper">
				<div class="header">
					<h5>Generation settings</h5>
				</div>

				<table class="table renderoutputchannelcontrols renderoutputchannelcontrols-{$variantid}">
					<thead>
						<tr class="active">
							<th width="20%">
								<xsl:text>Format</xsl:text>
							</th>

							<th width="20%">
								<xsl:text>Contents</xsl:text>
							</th>

							<xsl:if test="$renderlayoutoptions = 'yes'">
								<th width="20%">
									<xsl:text>Layout variations</xsl:text>
								</th>
							</xsl:if>

							<th width="60%">
								<xsl:attribute name="width">
									<xsl:choose>
										<xsl:when test="$renderlayoutoptions = 'yes'">40%</xsl:when>
										<xsl:otherwise>60%</xsl:otherwise>
									</xsl:choose>
								</xsl:attribute>
								<xsl:text>Options</xsl:text>
							</th>
						</tr>
					</thead>
					<tbody>
						<tr>
							<td class="outputchannel-format">
								<xsl:choose>
									<xsl:when test="$outputchanneltype = 'pdf'">
										<select class="form-control input-sm output-format" id="outputFormat{$variantid}">
											<option selected="selected" value="pdf">PDF</option>
											<option value="word">Word</option>
											<option value="excel">Excel</option>

											<!-- Data lineage reports -->
											<xsl:choose>
												<xsl:when test="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement[@ref-outputchannelvariant = $variantid]">
													<!-- Offer data lineage with a specific target model -->
													<xsl:for-each select="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement[@ref-outputchannelvariant = $variantid]">
														<xsl:if test="not(@ref-taxonomy) or not(preceding-sibling::reporting_requirement[@ref-outputchannelvariant = $variantid and @ref-taxonomy = current()/@ref-taxonomy])">
															<xsl:variable name="scheme">
																<xsl:choose>
																	<xsl:when test="@ref-taxonomy"><xsl:value-of select="@ref-taxonomy"/></xsl:when>
																	<xsl:otherwise><xsl:value-of select="@ref-mappingservice"/></xsl:otherwise>
																</xsl:choose>			
															</xsl:variable>
															
															<xsl:variable name="name">
																<xsl:choose>
																	<xsl:when test="@ref-taxonomy"><xsl:value-of select="@ref-taxonomy"/></xsl:when>
																	<xsl:otherwise><xsl:value-of select="name"/></xsl:otherwise>
																</xsl:choose>			
															</xsl:variable>
															
															<option data-reportingrequirementscheme="{$scheme}" value="datalineage">
																<xsl:if test="@disabled = 'true'">
																	<xsl:attribute name="disabled">disabled</xsl:attribute>
																</xsl:if>
																<xsl:text>Data lineage (target: </xsl:text>
																<xsl:value-of select="$name"/>
																<xsl:text>)</xsl:text>
															</option>
														</xsl:if>
													</xsl:for-each>
												</xsl:when>
												<xsl:when test="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement">
													<!-- Offer data lineage as a generic option, targeting the last model we can find in the reporting requirements defined for this project -->
													<xsl:for-each select="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement[position() = last()]">
														<option data-reportingrequirementscheme="{@ref-mappingservice}" value="datalineage">
															<xsl:if test="@disabled = 'true'">
																<xsl:attribute name="disabled">disabled</xsl:attribute>
															</xsl:if>
															<xsl:text>Data lineage</xsl:text>
														</option>
													</xsl:for-each>
												</xsl:when>
												<xsl:otherwise>
													<!-- Offer data lineage using a fixed value pointing -->
													<option data-reportingrequirementscheme="generic" value="datalineage">
														<xsl:text>Data lineage</xsl:text>
													</option>
												</xsl:otherwise>
											</xsl:choose>
											
											<!-- Collision reports -->
											<xsl:for-each select="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement[@ref-outputchannelvariant = $variantid]">
												<xsl:if test="not(@ref-taxonomy) or not(preceding-sibling::reporting_requirement[@ref-outputchannelvariant = $variantid and @ref-taxonomy = current()/@ref-taxonomy])">
													<xsl:variable name="scheme">
														<xsl:choose>
															<xsl:when test="@ref-taxonomy"><xsl:value-of select="@ref-taxonomy"/></xsl:when>
															<xsl:otherwise><xsl:value-of select="@ref-mappingservice"/></xsl:otherwise>
														</xsl:choose>			
													</xsl:variable>
													
													<xsl:variable name="name">
														<xsl:choose>
															<xsl:when test="@ref-taxonomy"><xsl:value-of select="@ref-taxonomy"/></xsl:when>
															<xsl:otherwise><xsl:value-of select="name"/></xsl:otherwise>
														</xsl:choose>			
													</xsl:variable>
													
													<option data-reportingrequirementscheme="{$scheme}" value="collisionreport">
														<xsl:if test="@disabled = 'true'">
															<xsl:attribute name="disabled">disabled</xsl:attribute>
														</xsl:if>
														<xsl:text>Collision report (target: </xsl:text>
														<xsl:value-of select="$name"/>
														<xsl:text>)</xsl:text>
													</option>
												</xsl:if>
											</xsl:for-each>

											<!-- XBRL reports -->
											<xsl:for-each select="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement[@ref-outputchannelvariant = $variantid]">
												<xsl:choose>
													<xsl:when test="contains(@format, ',')">
														<xsl:variable name="format-one" select="substring-before(@format, ',')"/>
														<xsl:variable name="format-two" select="substring-after(@format, ',')"/>
														<xsl:variable name="name-one">
															<xsl:call-template name="string-replace-all">
																<xsl:with-param name="text" select="name"/>
																<xsl:with-param name="replace">[placeholder]</xsl:with-param>
																<xsl:with-param name="by">
																	<xsl:text>(</xsl:text>
																	<xsl:value-of select="translate($format-one, $lowercase, $uppercase)"/>
																	<xsl:text>)</xsl:text>
																</xsl:with-param>
															</xsl:call-template>
														</xsl:variable>
														<xsl:variable name="name-two">
															<xsl:call-template name="string-replace-all">
																<xsl:with-param name="text" select="name"/>
																<xsl:with-param name="replace">[placeholder]</xsl:with-param>
																<xsl:with-param name="by">
																	<xsl:text>(</xsl:text>
																	<xsl:value-of select="translate($format-two, $lowercase, $uppercase)"/>
																	<xsl:text>)</xsl:text>
																</xsl:with-param>
															</xsl:call-template>
														</xsl:variable>
														
														<option data-reportingrequirementscheme="{@ref-mappingservice}" value="{$format-one}">
															<xsl:if test="@disabled = 'true'">
																<xsl:attribute name="disabled">disabled</xsl:attribute>
															</xsl:if>
															<xsl:value-of select="$name-one"/>
														</option>
														<option data-reportingrequirementscheme="{@ref-mappingservice}" value="{$format-two}">
															<xsl:if test="@disabled = 'true'">
																<xsl:attribute name="disabled">disabled</xsl:attribute>
															</xsl:if>
															<xsl:value-of select="$name-two"/>
														</option>
														
						
														
													</xsl:when>
													<xsl:otherwise>
														<option data-reportingrequirementscheme="{@ref-mappingservice}" value="{@format}">
															<xsl:if test="@disabled = 'true'">
																<xsl:attribute name="disabled">disabled</xsl:attribute>
															</xsl:if>
															<xsl:value-of select="name"/>
														</option>
													</xsl:otherwise>
												</xsl:choose>
												
												
												
											</xsl:for-each>
										</select>
										<!--
									<div class="radio">
										<label><input type="radio" name="outputFormat{$variantid}" id="outputFormat{$variantid}-pdf" value="pdf" checked="checked"/> PDF</label>
									</div>
									<div class="radio">
										<label><input type="radio" name="outputFormat{$variantid}" id="outputFormat{$variantid}-msword" value="word"/> Word</label>
									</div>
									<div class="radio">
										<label><input type="radio" name="outputFormat{$variantid}" id="outputFormat{$variantid}-msexcel" value="excel"/> Excel</label>
									</div>
									<xsl:for-each select="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement[@ref-outputchannelvariant = $variantid]">
										<div class="radio">
											<xsl:attribute name="class">
												<xsl:text>radio</xsl:text>
												<xsl:if test="@disabled = 'true'">
													<xsl:text> disabled</xsl:text>
												</xsl:if>
											</xsl:attribute>
											<label>
												<!-\- ref-mappingservice="PHG2017" ref-outputchannelvariant="20fpdf" format="ixbrl" regulator="sec"> -\->
												<input type="radio" name="outputFormat{$variantid}" id="outputFormat{$variantid}-{@ref-mappingservice}" data-reportingrequirementscheme="{@ref-mappingservice}" value="{@format}">
													<xsl:if test="@disabled = 'true'">
														<xsl:attribute name="disabled">disabled</xsl:attribute>
													</xsl:if>
												</input>
												<xsl:text> </xsl:text>
												<xsl:value-of select="name"/>
											</label>
										</div>
									</xsl:for-each>-->
									</xsl:when>
									<xsl:when test="$outputchanneltype = 'website'">
										<select class="form-control input-sm" id="outputFormat{$variantid}">
											<option selected="selected" value="website">Zip</option>
											<option value="pdf">PDF</option>
											<option value="word">Word</option>

											<!-- Data lineage reports -->
											<xsl:for-each select="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement[position() = last()]">
												<option data-reportingrequirementscheme="{@ref-mappingservice}" value="datalineage">
													<xsl:if test="@disabled = 'true'">
														<xsl:attribute name="disabled">disabled</xsl:attribute>
													</xsl:if>
													<xsl:text>Data lineage</xsl:text>
												</option>
											</xsl:for-each>

										</select>
										<!--<div class="radio">
										<label><input type="radio" name="outputFormat{$variantid}" id="outputFormat{$variantid}-zip" value="website" checked="checked"/> ZIP</label>
									</div>-->
									</xsl:when>
									<xsl:when test="$outputchanneltype = 'htmlsite'">
										<select class="form-control input-sm" id="outputFormat{$variantid}">
											<option selected="selected" value="website">Zip</option>
											<option selected="selected" value="awss3">Publish to server</option>
										</select>
										
										<!--<div class="radio">
										<label><input type="radio" name="outputFormat{$variantid}" id="outputFormat{$variantid}-zip" value="website" checked="checked"/> ZIP</label>
									</div>-->
									</xsl:when>
									<xsl:otherwise>
										<div class="alert alert-warning" role="alert">No report output format defined for <xsl:value-of select="$outputchanneltype"/></div>
									</xsl:otherwise>
								</xsl:choose>

								<div class="generated-reports-controls">
									<button type="button" class="btn btn-info btn-xs" data-projectid="{$project-id}" data-outputchanneltype="{$outputchanneltype}" data-editorid="{$editor-id}" data-reporttypeid="{$reporttype-id}" data-outputchannelvariantid="{$variantid}" data-outputchannellanguage="{$variantlanguage}">
										<span class="glyphicon glyphicon-list-alt"><xsl:comment>.</xsl:comment></span>
										<xsl:text>XBRL reports history</xsl:text>
									</button>
								</div>
							</td>
							<td class="outputchannel-contents">
								<div class="radio">
									<label>
										<input type="radio" name="outputContents{$variantid}" id="outputContents{$variantid}-all" value="all" checked="checked"/>
										<xsl:text> </xsl:text>
										<xsl:choose>
											<xsl:when test="$outputchanneltype = 'pdf'">Complete report</xsl:when>
											<xsl:when test="$outputchanneltype = 'website'">Full website</xsl:when>
											<xsl:otherwise>All content</xsl:otherwise>
										</xsl:choose>
									</label>
								</div>
								<xsl:if test="$outputchanneltype = 'pdf'">
									<div class="radio customselection">
										<label>
											<input type="radio" name="outputContents{$variantid}" id="outputContents{$variantid}-custom" value="custom"/>
											<xsl:text> Custom selection</xsl:text>
											<div class="customselectionwrapper">
												<button type="button" class="btn btn-default btn-xs custom-selection" data-projectid="{$project-id}" data-outputchanneltype="{$outputchanneltype}" data-editorid="{$editor-id}" data-reporttypeid="{$reporttype-id}" data-outputchannelvariantid="{$variantid}" data-outputchannellanguage="{$variantlanguage}">
													<xsl:text>Define sections</xsl:text>
												</button>
												<div>
													<small>
														<xsl:text> </xsl:text>
													</small>
												</div>
											</div>
										</label>
									</div>
								</xsl:if>
							</td>

							<xsl:if test="$renderlayoutoptions = 'yes'">
								<td class="outputchannel-layouts">
									<!-- Render options -->
									<xsl:apply-templates select="../../layouts/layout">
										<xsl:with-param name="variantid" select="$variantid"/>
										<xsl:with-param name="defaultlayout">
											<xsl:choose>
												<xsl:when test="@defaultlayout">
													<xsl:value-of select="@defaultlayout"/>
												</xsl:when>
												<xsl:otherwise>regular</xsl:otherwise>
											</xsl:choose>
										</xsl:with-param>
									</xsl:apply-templates>
								</td>
							</xsl:if>

							<td class="outputchannel-options">
								<!-- Render options -->
								<xsl:apply-templates select="../../layouts/options">
									<xsl:with-param name="filter">asmsword,asmsexcel</xsl:with-param>
									<xsl:with-param name="variantid" select="$variantid"/>
								</xsl:apply-templates>
								<xsl:if test="$outputchanneltype = 'pdf'">
									<span class="bulkgeneration">
										<xsl:if test="not(contains($permissions, 'useextratools'))">
											<xsl:attribute name="style">
												<xsl:text>display:none</xsl:text>
											</xsl:attribute>							
										</xsl:if>
										
										<div class="checkbox">
											<label>
												<input type="checkbox" class="tx-bulkgenerate" id="tx-bulkgenerate-{$variantid}"/> Bulk generate <small>(all output channels at once)</small>
											</label>
										</div>
									</span>
								</xsl:if>
								
								<xsl:if test="$outputchanneltype = 'htmlsite'">
									<!-- placeholders for rendering XBRL filings that you may want to publish along with the website -->
									<xsl:variable name="pdfoutputchannel-samelanng">
										<xsl:value-of select="/configuration/editors/editor[@id=$editor-id]/output_channels/output_channel[@type='pdf']/variants/variant[@lang=$variantlanguage]/@id"/>
									</xsl:variable>
									
									<xsl:if test="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement[@ref-outputchannelvariant=$pdfoutputchannel-samelanng]">
										<xsl:for-each select="/configuration/cms_projects/cms_project[@id = $project-id]/reporting_requirements/reporting_requirement[@ref-outputchannelvariant=$pdfoutputchannel-samelanng]">
											
											<div class="checkbox renderoptions" data-type="xbrlpackage" data-pdfvariantid="{$pdfoutputchannel-samelanng}" data-ref-mappingservice="{./@ref-mappingservice}">
												<xsl:comment>Render <xsl:value-of select="name"/> XBRL packages here</xsl:comment>
											</div>
										</xsl:for-each>
									</xsl:if>
									
								</xsl:if>
							</td>

						</tr>
					</tbody>
				</table>

				<div class="generatebuttonwrapper">
					<button type="button" class="btn btn-primary btn-sm render-renderoutputchannel" data-projectid="{$project-id}" data-editorid="{$editor-id}" data-reporttypeid="{$reporttype-id}" data-outputchannelvariantid="{$variantid}" data-outputchannellanguage="{$variantlanguage}" data-octype="{$outputchanneltype}">
						<xsl:text>Generate</xsl:text>
					</button>
					
					<xsl:choose>
						<xsl:when test="$outputchanneltype = 'htmlsite'">
							<a class="btn btn-info btn-sm open-site" data-projectid="{$project-id}" data-outputchanneltype="{$outputchanneltype}" data-editorid="{$editor-id}" data-reporttypeid="{$reporttype-id}" data-outputchannelvariantid="{$variantid}" data-outputchannellanguage="{$variantlanguage}" href="#" target="_blank"><i class="glyphicon glyphicon-share-alt"><!--.--></i> Open site</a>
							
						</xsl:when>
					</xsl:choose>
					
				</div>


			</div>



		</div>

	</xsl:template>

	<xsl:template match="options">
		<xsl:param name="filter"/>
		<xsl:param name="variantid"/>
		
		<!-- Check if in this output channel we have configured an XBRL reporting requirement -->
		<xsl:variable name="canrenderxbrl">
			<xsl:choose>
				<xsl:when test="/configuration/cms_projects/cms_project[@id=$project-id]/reporting_requirements/reporting_requirement[contains(@ref-outputchannelvariant, $variantid)]">yes</xsl:when>
				<xsl:otherwise>no</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>
		
		<!--
			<div>type: <xsl:value-of select="local-name(ancestor::*[2])"/> - <xsl:value-of select="ancestor::*[2]/@type"/></div>
			<div>contains-disable: <xsl:value-of select="option[@id='disablexbrlvalidation']/text()"/></div>
			<div>canrenderxbrl: <xsl:value-of select="$canrenderxbrl"/></div>
		-->
		
		<div class="form-group">
			<xsl:choose>
				<xsl:when test="option">
					<xsl:apply-templates select="option">
						<xsl:with-param name="filter" select="$filter"/>
					</xsl:apply-templates>
				</xsl:when>
				<xsl:otherwise>
					<div>No options available...</div>
				</xsl:otherwise>
			</xsl:choose>
			
			<div class="checkbox renderoptions">
				<xsl:if test="not(contains($permissions, 'useextratools'))">
					<xsl:attribute name="style">
						<xsl:text>display:none</xsl:text>
					</xsl:attribute>							
				</xsl:if>

				<xsl:if test="$canrenderxbrl='yes'">
					<xsl:if test="not(option[@id='disablexbrlvalidation'])">
						<label>
							<input type="checkbox" name="disablexbrlvalidation" data-type="normal"/> Disable XBRL validation
						</label>
					</xsl:if>
					<label>
						<input type="checkbox" name="includexbrllogs" data-type="normal"/> Include XBRL generation logs
					</label>
				</xsl:if>
			</div>
		</div>
	</xsl:template>

	<xsl:template match="option">
		<xsl:param name="filter"/>
		<xsl:if test="not(contains($filter, @id))">
			<div class="checkbox renderoptions">
				<label>
					<input type="checkbox" name="{@id}">
						<xsl:attribute name="data-type">
							<xsl:choose>
								<xsl:when test="@type">
									<xsl:value-of select="@type"/>
								</xsl:when>
								<xsl:otherwise>normal</xsl:otherwise>
							</xsl:choose>
						</xsl:attribute>
						<xsl:if test="@selected = 'true'">
							<xsl:attribute name="checked">checked</xsl:attribute>
						</xsl:if>
						<xsl:if test="@data-value">
							<xsl:attribute name="data-value">
								<xsl:value-of select="@data-value"/>
							</xsl:attribute>
						</xsl:if>
					</input>
					<xsl:text> </xsl:text>
					<xsl:value-of select="."/>
				</label>
			</div>
		</xsl:if>
	</xsl:template>

	<xsl:template match="layout">
		<xsl:param name="variantid"/>
		<xsl:param name="defaultlayout"/>
		<xsl:variable name="layoutid" select="@id"/>
		<xsl:variable name="userpreferencelayout">
			<xsl:value-of select="/configuration/projectpreferences/outputchannels/variant[@id = $variantid]/@layout"/>
		</xsl:variable>

		<!--
		<p>variantid: <xsl:value-of select="$variantid"/></p>
		<p>defaultlayout: <xsl:value-of select="$defaultlayout"/></p>
		<p>layoutid: <xsl:value-of select="$layoutid"/></p>
		<p>userpreferencelayout: <xsl:value-of select="$userpreferencelayout"/></p>
		-->

		<div class="radio">
			<label>
				<input type="radio" name="outputChannelLayout{$variantid}" id="outputChannelLayout{$variantid}-{@id}" data-ocvariantid="{$variantid}" value="{@id}">
					<xsl:choose>
						<!-- is there a user project preference setting -->
						<xsl:when test="/configuration/projectpreferences/outputchannels/variant[@id = $variantid]">
							<xsl:if test="$layoutid = $userpreferencelayout">
								<xsl:attribute name="checked">checked</xsl:attribute>
							</xsl:if>
						</xsl:when>
						<!-- has a default layout been set-->
						<xsl:when test="$layoutid = $defaultlayout">
							<xsl:attribute name="checked">checked</xsl:attribute>
						</xsl:when>
					</xsl:choose>
				</input>
				<xsl:text> </xsl:text>
				<xsl:value-of select="name"/>
			</label>
		</div>


	</xsl:template>



</xsl:stylesheet>
