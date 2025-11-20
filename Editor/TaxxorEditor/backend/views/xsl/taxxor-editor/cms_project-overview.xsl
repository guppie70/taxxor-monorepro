<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:include href="_utils.xsl"/>

	<xsl:param name="default_client"/>
	<xsl:param name="render_chrome">no</xsl:param>
	<xsl:param name="calendar_data"/>
    <xsl:param name="permissions"/>
    <xsl:param name="uristaticassets"/>

	<xsl:variable name="nonbreaking_space">&#160;</xsl:variable>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>



	<xsl:template match="/">
		<xsl:variable name="nodelist_clients_access" select="//clients/client[.//entity/@access='true']"/>
		<xsl:variable name="nodelist_clients_dropdown" select="//clients/client[.//entity/@access='true']"/>
		
		<xsl:choose>
			<xsl:when test="$render_chrome='no'">
				<!-- only render the content of the div#result node -->
				<div>
					<xsl:call-template name="render_clients">
						<xsl:with-param name="nodelist_clients_access" select="$nodelist_clients_access"/>
					</xsl:call-template>					
				</div>
			</xsl:when>
			<xsl:otherwise>
				<!-- render the complete html -->

				<!-- 
				<xsl:value-of select="$default_client"/>
				-->

				<h5 class="row header smaller lighter blue">
					<!-- <span class="col-xs-6">
						<xsl:call-template name="get-localized-value-by-key">
							<xsl:with-param name="doc-translations" select="/"/>
							<xsl:with-param name="id">
								<xsl:text>cms_overview_page-top-header</xsl:text>
							</xsl:with-param>
						</xsl:call-template>
					</span> -->

					<!-- drop down menu to select other entities -->
					<xsl:if test="count($nodelist_clients_dropdown) &gt; 1">
						<div class="widget-toolbar col-xs-12">
							<div class="btn-group pull-right">
								<button class="btn btn-sm btn-primary btn-white no-border dropdown-toggle" data-toggle="dropdown" id="dropdownMenu0">
									<xsl:text>Choose legal entity</xsl:text>
									<xsl:value-of select="$nonbreaking_space"/>
									<i class="ace-icon fa fa-angle-down icon-on-right"/>
								</button>

								<ul class="dropdown-menu" role="menu" aria-labelledby="dropdownMenu0">
									<xsl:for-each select="$nodelist_clients_dropdown">
										<xsl:sort select="name" data-type="text"/>
										<li role="presentation" id="select-client-{@id}">
											<!-- default selection -->
											<xsl:choose>
												<xsl:when test="string-length(normalize-space($default_client))=0">
													<xsl:if test="position()=1">
														<xsl:attribute name="class">active</xsl:attribute>
													</xsl:if>
												</xsl:when>
												<xsl:otherwise>
													<xsl:if test="@id=$default_client">
														<xsl:attribute name="class">active</xsl:attribute>
													</xsl:if>
												</xsl:otherwise>
											</xsl:choose>

											<a role="menuitem" class="clientfilter" tabindex="-1" data-clientid="{@id}" href="">
												<xsl:value-of select="name"/>
											</a>
										</li>
									</xsl:for-each>
								</ul>
							</div>
						</div>
					</xsl:if>


				</h5>




				<div class="accordion-style1 panel-group accordion-style2" id="accordion">


					<div class="panel panel-default" id="existing-filings">
						<div class="panel-heading">
							<h4 class="panel-title">
								<a href="#collapseOne" data-parent="#accordion" data-toggle="collapse" class="accordion-toggle">
									<i data-icon-show="ace-icon fa fa-angle-right" data-icon-hide="ace-icon fa fa-angle-down" class="ace-icon fa fa-angle-down bigger-110"/>
									<xsl:text>&#160;</xsl:text>
									<xsl:call-template name="get-localized-value-by-key">
										<xsl:with-param name="doc-translations" select="/"/>
										<xsl:with-param name="id">
											<xsl:text>label_existing-filings</xsl:text>
										</xsl:with-param>
									</xsl:call-template>
								</a>
							</h4>
						</div>

						<div id="collapseOne" class="panel-collapse collapse in">
							<div class="panel-body">

								<div id="result">
									<xsl:call-template name="render_clients">
										<xsl:with-param name="nodelist_clients_access" select="$nodelist_clients_access"/>
									</xsl:call-template>
								</div>

							</div>
						</div>


					</div>

					<div class="panel panel-default" id="upcoming-filings">
						<div class="panel-heading">
							<h4 class="panel-title">
								<a href="#collapseTwo" data-parent="#accordion" data-toggle="collapse" class="accordion-toggle collapsed">
									<i data-icon-show="ace-icon fa fa-angle-right" data-icon-hide="ace-icon fa fa-angle-down" class="ace-icon fa fa-angle-right bigger-110"/>
									<xsl:text>&#160;</xsl:text>
									<xsl:call-template name="get-localized-value-by-key">
										<xsl:with-param name="doc-translations" select="/"/>
										<xsl:with-param name="id">
											<xsl:text>label_upcoming-filings</xsl:text>
										</xsl:with-param>
									</xsl:call-template>
									<span class="badge badge-danger pull-right inline">2</span>
								</a>
							</h4>
						</div>

						<div id="collapseTwo" class="panel-collapse collapse">
							<div class="panel-body">
								<h3 class="header smaller lighter blue">Requirements Calendar</h3>

								<table class="table-condensed" width="100%">
									<tr valign="top">
										<td width="70%"> The Taxxor Regulation DB has populated the calendar below with dates for your upcoming filings.<br/>
											<br/>
											<button class="btn btn-info btn-xs" data-toggle="modal" data-target="#reportMissingRequirementModal">
												<i class="glyphicon glyphicon-envelope"/>
												<xsl:text> Report missing requirement</xsl:text>
											</button>
										</td>
										<td width="30%">
											<strong>Filing legenda:</strong>
											<br/>
											<span style="width: 12px; height: 10px; margin-right: 10px;" class="label-purple">
												<img src="{$uristaticassets}/images/t.gif" alt="" width="12" height="10"/>
											</span>
											<xsl:text> Filing open</xsl:text>
											<br/>

											<span style="width: 12px; height: 10px; margin-right: 7px;" class="label-grey">
												<img src="{$uristaticassets}/images/t.gif" alt="" width="15" height="10"/>
											</span>
											<xsl:text> Filing in progress</xsl:text>
											<br/>

											<span style="width: 12px; height: 10px; margin-right: 7px;" class="label-warning">
												<img src="{$uristaticassets}/images/t.gif" alt="" width="15" height="10"/>
											</span>
											<xsl:text> Filing submitted</xsl:text>

										</td>
									</tr>
								</table>






								<div class="space-6"/>
								<div id="calendar">
									<xsl:text> </xsl:text>
								</div>
							</div>
						</div>
					</div>

				</div>

			</xsl:otherwise>
		</xsl:choose>


	</xsl:template>

	<xsl:template name="render_clients">
		<xsl:param name="nodelist_clients_access"/>

		<xsl:choose>
			<xsl:when test="count($nodelist_clients_access) &gt; 0">
				<xsl:apply-templates select="$nodelist_clients_access">
					<xsl:sort select="name" data-type="text"/>
				</xsl:apply-templates>
			</xsl:when>
			<xsl:otherwise>
				<div class="alert alert-warning">
					<strong>Warning: </strong> No filings created yet or you do not have sufficient rights to edit them. <br/> Please create a new filing using the calendar overview under the "Upcoming filings" section below. </div>
			</xsl:otherwise>
		</xsl:choose>

	</xsl:template>

	<xsl:template match="client">

		<xsl:apply-templates select="entity_groups/entity_group|entity">
			<xsl:with-param name="client_position" select="position()"/>
			<xsl:with-param name="root_id" select="@id"/>
			<xsl:with-param name="client_name" select="name"/>
			<xsl:sort select="name" data-type="text"/>
		</xsl:apply-templates>


	</xsl:template>

	<xsl:template match="entity_group">
		<xsl:param name="client_position"/>
		<xsl:param name="root_id"/>
		<xsl:param name="client_name"/>
		
		<xsl:choose>
			<xsl:when test="@sort='none'">
				<xsl:apply-templates select="entity">
					<xsl:with-param name="client_position" select="$client_position"/>
					<xsl:with-param name="root_id" select="$root_id"/>
					<xsl:with-param name="client_name" select="$client_name"/>
					<xsl:with-param name="entity_group_name" select="name"/>
				</xsl:apply-templates>
			</xsl:when>
			<xsl:otherwise>
				<xsl:apply-templates select="entity">
					<xsl:with-param name="client_position" select="$client_position"/>
					<xsl:with-param name="root_id" select="$root_id"/>
					<xsl:with-param name="client_name" select="$client_name"/>
					<xsl:with-param name="entity_group_name" select="name"/>
					<xsl:sort select="name" data-type="text"/>
				</xsl:apply-templates>						
			</xsl:otherwise>
		</xsl:choose>				



	</xsl:template>

	<xsl:template match="entity">
		<xsl:param name="client_position"/>
		<xsl:param name="root_id"/>
		<xsl:param name="client_name"/>
		<xsl:param name="entity_group_name"/>
		
		<xsl:variable name="reporttype_list" select="/configuration/report_types/report_type"/>
		<xsl:variable name="project_list" select="projects/cms_project[@access='true']"/>
		<xsl:variable name="legal_entity_guid" select="@guidLegalEntity"/>
		<xsl:variable name="has-developertools-permission">
			<xsl:choose>
				<xsl:when test="/configuration/permissions/items/item/permissions/permission[@id='viewdevelopertools'] or contains($permissions, 'viewdevelopertools')">yes</xsl:when>
				<xsl:otherwise>no</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>
		
		<xsl:choose>
			<xsl:when test="count($project_list) &gt; 0 or $has-developertools-permission = 'yes'">
				
				<div class="client-wrapper" id="client_{$root_id}" data-entityguid="{$legal_entity_guid}">
					<!-- default selection -->
					<xsl:choose>
						<xsl:when test="string-length(normalize-space($default_client))=0">
							<xsl:if test="number($client_position)=1">
								<xsl:attribute name="style">display: block</xsl:attribute>
							</xsl:if>
						</xsl:when>
						<xsl:otherwise>
							<xsl:if test="$root_id=$default_client">
								<xsl:attribute name="style">display: block</xsl:attribute>
							</xsl:if>
						</xsl:otherwise>
					</xsl:choose>
		
					<h4 class="header smaller lighter reporttypecount-{projects/@reporttypecount}">
						<xsl:choose>
							<xsl:when test="string-length(normalize-space($entity_group_name))=0 ">
								<xsl:value-of select="$client_name"/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:value-of select="$entity_group_name"/>
							</xsl:otherwise>
						</xsl:choose>
						<xsl:text disable-output-escaping="yes"> &gt; </xsl:text>
						<xsl:value-of select="name"/>
					</h4>
					<xsl:choose>
						<xsl:when test="count($project_list) &gt; 0">
							<div class="filter-wrapper reporttypecount-{projects/@reporttypecount}">
								<div class="filter btn btn-info btn-xs btn-all active" data-filter="all" data-entityguid="{$legal_entity_guid}">
									<xsl:text>Show all</xsl:text>
								</div>
								<xsl:for-each select="$reporttype_list">
									<xsl:variable name="current_report_type_id" select="@id"/>
									<xsl:variable name="current_report_type_name" select="name"/>
									<xsl:if test="count($project_list[@report-type=$current_report_type_id]) &gt; 0">
										<div class="filter btn btn-info btn-xs" data-filter="{$current_report_type_id}" data-entityguid="{$legal_entity_guid}">
											<xsl:value-of select="$current_report_type_name"/>
										</div>
									</xsl:if>
								</xsl:for-each>
							</div>
							<div class="reports-wrapper">
								<xsl:for-each select="$reporttype_list">
									<xsl:sort data-type="text" select="name"/>
									<xsl:variable name="current_report_type_id" select="@id"/>
									<xsl:variable name="current_report_type_name" select="name"/>
									<xsl:if test="count($project_list[@report-type=$current_report_type_id]) &gt; 0">
										<xsl:apply-templates select="$project_list[@report-type=$current_report_type_id]">
											<xsl:sort data-type="text" select="@sortstring"/>
											<xsl:sort data-type="text" select="name"/>
											<xsl:with-param name="type" select="$current_report_type_id" />
										</xsl:apply-templates>
									</xsl:if>
		
								</xsl:for-each>
							</div>
						</xsl:when>
						<xsl:otherwise>
							<div class="alert alert-info">
								<xsl:choose>
									<xsl:when test="/configuration/permissions/items/item/permissions/permission[@id='createfilingdocument'] or contains($permissions, 'createfilingdocument')">
										<xsl:text>No projects found, please use the button below to create a new one.</xsl:text>
									</xsl:when>
									<xsl:otherwise>
										<xsl:text>No projects found.</xsl:text>
									</xsl:otherwise>
								</xsl:choose>
							</div>
						</xsl:otherwise>
					</xsl:choose>
					<xsl:if test="/configuration/permissions/items/item/permissions/permission[@id='createfilingdocument'] or contains($permissions, 'createfilingdocument')">
		                <div class="modal-footer">
		                	<!--
		                	<xsl:if test="/configuration/permissions/items/item/permissions/permission[@id='createfilingdocument']">
		                		<span>In XML</span>
		                	</xsl:if>
		                	<xsl:if test="contains($permissions, 'createfilingdocument')">
		                		<span>In permissions string</span>
		                	</xsl:if>
		                	-->
		                    <button data-guid="{@guidLegalEntity}" class="btn btn-primary btn-xs btn-addproject"><span class="glyphicon glyphicon-plus-sign"><xsl:text> </xsl:text></span> Create new</button>
		                </div>
		            </xsl:if>
		
				</div>
			</xsl:when>
			<xsl:otherwise>
				<xsl:comment>Legal entity <xsl:value-of select="$legal_entity_guid"/> hidden because there are no projects in it yet and this user does not have sufficient permissions to create a new one</xsl:comment>
			</xsl:otherwise>
		</xsl:choose>
		
		

		
	</xsl:template>

	<xsl:template match="cms_project">
		<xsl:param name="type"/>
		<xsl:variable name="report_type_id" select="@report-type"/>
		<xsl:variable name="editor_id" select="/configuration/report_types/report_type[@id=$report_type_id]/@editorId"/>
		<xsl:variable name="report_editor_path" select="/configuration/editors/editor[@id=$editor_id]/path"/>
		<xsl:variable name="path_type" select="$report_editor_path/@path-type"/>
		<xsl:variable name="project_name">
			<xsl:call-template name="string-replace-all">
				<xsl:with-param name="text" select="name"/>
				<xsl:with-param name="replace">'</xsl:with-param>
				<xsl:with-param name="by">\'</xsl:with-param>
			</xsl:call-template>
		</xsl:variable>
		<xsl:variable name="status" select="versions/version[position()=last()]/status"/>
		<xsl:variable name="url" select="concat(/configuration/general/locations/location[@id=$path_type], $report_editor_path, '/filingcomposer/redirect.html?pid=', @id, '&amp;killcache=true')"/>
		<xsl:variable name="guidCalendarEvent">
			<xsl:if test="@guidCalendarEvent">
				<xsl:value-of select="@guidCalendarEvent"/>
			</xsl:if>
		</xsl:variable>
		<xsl:variable name="project-id">
			<xsl:value-of select="@id"/>
		</xsl:variable>
		<xsl:variable name="reporting-period" select="reporting_period"/>

		<a href="{$url}">
			<xsl:attribute name="class">report <xsl:value-of select="$type"/> projectstatus_<xsl:value-of select="$status"/></xsl:attribute>

			<div class="col1 mouse-pointer" onclick="location.href='{$url}'">
				<xsl:if test="$status='closed'">
					<span class="glyphicon glyphicon-lock"> </span>
				</xsl:if>
				<xsl:value-of select="name"/>

			</div>

			<!-- <div class="mouse-pointer" onclick="location.href='{$url}'">
				<xsl:value-of select="$status"/>
			</div>

			<div class="mouse-pointer" onclick="location.href='{$url}'">
				<xsl:choose>
					<xsl:when test="string-length($guidCalendarEvent) &gt; 0 and $calendar_data">
						<xsl:text>(</xsl:text>
						<xsl:value-of select="$calendar_data//reporting_requirement/requirement/nextFilings/requirementSchedule[identifier=$guidCalendarEvent]/startDate/@isoDate"/>
						<xsl:text> - </xsl:text>
						<xsl:value-of select="$calendar_data//reporting_requirement/requirement/nextFilings/requirementSchedule[identifier=$guidCalendarEvent]/endDate/@isoDate"/>
						<xsl:text>)</xsl:text>
					</xsl:when>
					<xsl:otherwise>
						<xsl:text> - </xsl:text>
					</xsl:otherwise>
				</xsl:choose>
			</div> 

			<div class="mouse-pointer filing-date" onclick="location.href='{$url}'">
				<xsl:choose>
					<xsl:when test="string-length($guidCalendarEvent) &gt; 0 and $calendar_data">
						<xsl:value-of select="$calendar_data//reporting_requirement/requirement/nextFilings/requirementSchedule[identifier=$guidCalendarEvent]/filingDate/@isoDate"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:text> - </xsl:text>
					</xsl:otherwise>
				</xsl:choose>
			</div> 
			-->

			
			<xsl:variable name="show-properties-button">
				<xsl:choose>
					<xsl:when test="/configuration/permissions/items/item[@projectid=$project-id]/permissions/permission[@id='all' or @id='manageacl' or @id='manageexternaldata' or @id='managestructureddata' or @id='clonefilingdocument' or @id='createfilingdocument']">yes</xsl:when>
					<xsl:otherwise>no</xsl:otherwise>
				</xsl:choose>
			</xsl:variable>
			
			<xsl:variable name="show-clone-button">
				<xsl:choose>
					<xsl:when test="/configuration/permissions/items/item[@projectid=$project-id]/permissions/permission[@id='all' or @id='clonefilingdocument']">yes</xsl:when>
					<xsl:otherwise>no</xsl:otherwise>
				</xsl:choose>
			</xsl:variable>
			
			<xsl:variable name="show-delete-button">
				<xsl:choose>
					<xsl:when test="/configuration/permissions/items/item[@projectid=$project-id]/permissions/permission[@id='all' or @id='deletefilingdocument']">yes</xsl:when>
					<xsl:otherwise>no</xsl:otherwise>
				</xsl:choose>
			</xsl:variable>
			
			<xsl:if test="$show-clone-button='yes' or $show-delete-button='yes' or $show-properties-button='yes'">
				<div>
					<div class="pull-right">
						<xsl:if test="$show-properties-button='yes'">
							<button data-toggle="modal" data-target="#propertiesModal" title="Project properties" data-content="View or edit the properties of this project" class="btn btn-default btn-xs" data-projectid="{@id}" data-projectname="{$project_name}" data-reporttypeid="{$report_type_id}" data-reportingperiod="{reporting_period/text()}">
								<span class="glyphicon glyphicon-cog">
									<xsl:text> </xsl:text>
								</span>
							</button>
						</xsl:if>
						
						<xsl:if test="$show-clone-button='yes'">
							<button data-toggle="modal" data-target="#cloneModal" title="Clone document" data-content="Clone this document" class="btn btn-info btn-xs" data-projectid="{@id}" data-projectname="{$project_name}" data-reporttypeid="{$report_type_id}" data-reportingperiod="{reporting_period/text()}">
								<span class="glyphicon glyphicon-duplicate">
									<xsl:text> </xsl:text>
								</span>
							</button>
						</xsl:if>
						
						<xsl:if test="$show-delete-button='yes'">
							<button data-toggle="modal" data-target="#deleteModal" title="Delete document" data-content="Delete this document" class="btn btn-danger btn-xs" data-projectid="{@id}" data-projectname="{$project_name}" data-reporttypeid="{$report_type_id}" data-reportingperiod="{reporting_period/text()}">
								<span class="glyphicon glyphicon-trash">
									<xsl:text> </xsl:text>
								</span>
							</button>
						</xsl:if>						
					</div>
				</div>
			</xsl:if>
		</a>


	</xsl:template>

</xsl:stylesheet>
