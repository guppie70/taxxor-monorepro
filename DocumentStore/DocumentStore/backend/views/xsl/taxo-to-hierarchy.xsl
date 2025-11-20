<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:xbrli="http://www.xbrl.org/2003/instance" xmlns:xlink="http://www.w3.org/1999/xlink" xmlns:linkbase="http://www.xbrl.org/2003/linkbase" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:link="http://www.xbrl.org/2003/linkbase" xmlns:lextr="http://lextr.net/alfa/20150401" xmlns:lextr-types="http://lextr.net/types/20150401" version="1.0">

	<!-- global variables -->
	<xsl:variable name="newline">
		<xsl:text>
			
		</xsl:text>
	</xsl:variable>

	<xsl:variable name="label-linkbase" select="document(/xsd:schema/xsd:annotation/xsd:appinfo/link:linkbaseRef[@xlink:role='http://www.xbrl.org/2003/role/labelLinkbaseRef']/@xlink:href)"/>
	<xsl:variable name="presentation-linkbase" select="document(/xsd:schema/xsd:annotation/xsd:appinfo/link:linkbaseRef[@xlink:role='http://www.xbrl.org/2003/role/presentationLinkbaseRef']/@xlink:href)"/>
	<xsl:variable name="nodelist-instance-elements" select="/xsd:schema/xsd:element"/>
	<xsl:variable name="nodelist-appinfo" select="/xsd:schema/xsd:annotation/xsd:appinfo"/>


	<xsl:output indent="yes" method="xml"/>

	<xsl:template match="/">
		<!--
		<xsl:call-template name="render-debug-info"/>	
		-->

		<hierarchies>
			<!-- build up the hierarchies by starting from the presentation linkbase -->
			<xsl:apply-templates select="$presentation-linkbase/linkbase:linkbase/linkbase:presentationLink"/>
			
			<facts>
				<xsl:apply-templates select="/xsd:schema/xsd:element"/>
			</facts>
		</hierarchies>
	</xsl:template>
	
	<xsl:template match="xsd:element">
		<item id="{@id}" nodeName="{@name}" dataType="{substring-after(@type, ':')}" periodType="{@xbrli:periodType}"/>
	</xsl:template>

	<!-- 
		renders the output channels and the root section (presentation linkbase scope) 
	-->
	<xsl:template match="linkbase:presentationLink">
		<xsl:variable name="role-uri" select="@xlink:role"/>
		
		<output_channel id="{$nodelist-appinfo/link:roleType[@roleURI=$role-uri]/@id}" roleUri="{$role-uri}">
			<name>
				<xsl:value-of select="$nodelist-appinfo/link:roleType[@roleURI=$role-uri]/link:definition"/>
			</name>
			
			<!-- retrieve the label id of the top level item in the hierarchy -->
			<xsl:variable name="top-level-label-id">
				<xsl:call-template name="retrieve-top-level-label-id">
					<xsl:with-param name="presentation-arcs" select="linkbase:presentationArc"/>
					<xsl:with-param name="current-position">1</xsl:with-param>
				</xsl:call-template>
			</xsl:variable>
			<!--
			* <xsl:value-of select="$top-level-label-id"/>
			<xsl:value-of select="$newline"/>
			-->

			<xsl:variable name="taxonomy-reference">
				<xsl:call-template name="retrieve-taxonomy-reference">
					<xsl:with-param name="label-id" select="$top-level-label-id"/>
				</xsl:call-template>
			</xsl:variable>

			<hierarchy>
				<item>
					<xsl:call-template name="render-item-attributes">
						<xsl:with-param name="taxonomy-reference" select="$taxonomy-reference"/>
						<xsl:with-param name="label-id" select="$top-level-label-id"/>
					</xsl:call-template>

					<xsl:call-template name="render-link-names">
						<xsl:with-param name="taxonomy-reference" select="$taxonomy-reference"/>
					</xsl:call-template>

					<!-- enter the level 1 elements from the presentation linkbase -->
					<xsl:if test="linkbase:presentationArc[@xlink:from=$top-level-label-id]">
						<sub_items>
							<xsl:apply-templates select="linkbase:presentationArc[@xlink:from=$top-level-label-id]">
								<xsl:sort select="@order" data-type="number" order="ascending"/>
							</xsl:apply-templates>
						</sub_items>
					</xsl:if>

				</item>
			</hierarchy>
		</output_channel>

	</xsl:template>

	<!-- 
		renders an item (scope presentation linkbase) 
	-->
	<xsl:template match="linkbase:presentationArc">
		<xsl:variable name="current-label-id" select="@xlink:to"/>

		<xsl:variable name="taxonomy-reference">
			<xsl:call-template name="retrieve-taxonomy-reference">
				<xsl:with-param name="label-id" select="$current-label-id"/>
			</xsl:call-template>
		</xsl:variable>

		<item>
			<xsl:call-template name="render-item-attributes">
				<xsl:with-param name="taxonomy-reference" select="$taxonomy-reference"/>
				<xsl:with-param name="label-id" select="$current-label-id"/>
			</xsl:call-template>

			<xsl:call-template name="render-link-names">
				<xsl:with-param name="taxonomy-reference" select="$taxonomy-reference"/>
			</xsl:call-template>
			
			<xsl:if test="../linkbase:presentationArc[@xlink:from=$current-label-id]">
				<sub_items>
					<xsl:apply-templates select="../linkbase:presentationArc[@xlink:from=$current-label-id]">
						<xsl:sort select="@order" data-type="number" order="ascending"/>
					</xsl:apply-templates>
				</sub_items>
			</xsl:if>
		</item>

	</xsl:template>



	<!-- 
	**** Utility templates ****
	-->

	<!--
		retrieves the level 0 (top level) page by comparing the @from and @to attributes in the presentationArc nodes 
	-->
	<xsl:template name="retrieve-top-level-label-id">
		<xsl:param name="presentation-arcs"/>
		<xsl:param name="current-position"/>

		<!-- generate a string of "to" id's -->
		<xsl:variable name="to-ids">
			<xsl:for-each select="$presentation-arcs">
				<xsl:text>|</xsl:text>
				<xsl:value-of select="@xlink:to"/>
				<xsl:text>|</xsl:text>
			</xsl:for-each>
		</xsl:variable>

		<!-- generate a string of "from" values that cannot be found in the "to" string we created above -->
		<xsl:variable name="no-match">
			<xsl:for-each select="$presentation-arcs">
				<xsl:variable name="search-string">
					<xsl:text>|</xsl:text>
					<xsl:value-of select="@xlink:from"/>
					<xsl:text>|</xsl:text>
				</xsl:variable>

				<xsl:if test="not(contains($to-ids,$search-string))">
					<xsl:value-of select="@xlink:from"/>
					<xsl:text>|</xsl:text>
				</xsl:if>

			</xsl:for-each>
		</xsl:variable>

		<!-- return the first value -->
		<xsl:value-of select="substring-before($no-match,'|')"/>
	</xsl:template>

	<!-- 
		uses the label id to retrieve the taxonomy reference from the presentation linkbase 
	-->
	<xsl:template name="retrieve-taxonomy-reference">
		<xsl:param name="label-id"/>

		<xsl:value-of select="$presentation-linkbase/linkbase:linkbase/linkbase:presentationLink/linkbase:loc[@xlink:label=$label-id]/@xlink:href"/>
	</xsl:template>

	<!-- 
		renders the linknames nodes by querying the label linkbase file
	-->
	<xsl:template name="render-link-names">
		<xsl:param name="taxonomy-reference"/>

		<xsl:variable name="label-arc-id">
			<xsl:value-of select="$label-linkbase/linkbase:linkbase/linkbase:labelLink/linkbase:loc[@xlink:href=$taxonomy-reference]/@xlink:label"/>
		</xsl:variable>

		<linknames>
			<xsl:choose>
				<xsl:when test="count($label-linkbase/linkbase:linkbase/linkbase:labelLink/linkbase:labelArc[@xlink:from=$label-arc-id]) &gt; 0">
					<xsl:for-each select="$label-linkbase/linkbase:linkbase/linkbase:labelLink/linkbase:labelArc[@xlink:from=$label-arc-id]">
						<xsl:variable name="label-id" select="@xlink:to"/>
						<xsl:call-template name="render-linkname">
							<xsl:with-param name="label-id" select="$label-id"/>
						</xsl:call-template>
					</xsl:for-each>
				</xsl:when>
				<xsl:otherwise>
					<xsl:variable name="taxonomy-element-id">
						<xsl:value-of select="substring-after($taxonomy-reference,'#')"/>
					</xsl:variable>
					<linkname>
						<xsl:text>[</xsl:text>
						<xsl:value-of select="$nodelist-instance-elements[@id=$taxonomy-element-id]/@name"/>
						<xsl:text>]</xsl:text>
					</linkname>
				</xsl:otherwise>
			</xsl:choose>
		</linknames>
	</xsl:template>

	<!-- 
		renders a single linkname node
	-->
	<xsl:template name="render-linkname">
		<xsl:param name="label-id"/>
		<xsl:for-each select="$label-linkbase/linkbase:linkbase/linkbase:labelLink/linkbase:label[@xlink:label=$label-id]">
			<linkname lang="{@xml:lang}">
				<xsl:value-of select="text()"/>
			</linkname>
		</xsl:for-each>
	</xsl:template>

	<!-- 
		renders a single linkname node
	-->
	<xsl:template name="render-item-attributes">
		<xsl:param name="taxonomy-reference"/>
		<xsl:param name="label-id"/>

		<xsl:variable name="taxonomy-filename">
			<xsl:value-of select="substring-before($taxonomy-reference,'#')"/>
		</xsl:variable>
		<xsl:variable name="taxonomy-element-id">
			<xsl:value-of select="substring-after($taxonomy-reference,'#')"/>
		</xsl:variable>
		
		<xsl:attribute name="id">
			<xsl:value-of select="$taxonomy-element-id"/>
		</xsl:attribute>
		<xsl:attribute name="labelId">
			<xsl:value-of select="$label-id"/>
		</xsl:attribute>
		<!--
		<xsl:attribute name="taxoReference">
			<xsl:value-of select="$taxonomy-reference"/>
		</xsl:attribute>
		-->
		<xsl:attribute name="nodeName">
			<xsl:value-of select="$nodelist-instance-elements[@id=$taxonomy-element-id]/@name"/>
		</xsl:attribute>
		<xsl:attribute name="dataType">
			<xsl:value-of select="substring-after($nodelist-instance-elements[@id=$taxonomy-element-id]/@type, ':')"/>
		</xsl:attribute>

	</xsl:template>

	<!-- 
		renders debug information	
	-->
	<xsl:template name="render-debug-info">

		<xsl:text>- </xsl:text>
		<xsl:value-of select="$label-linkbase/linkbase:linkbase/linkbase:labelLink[1]/linkbase:label[1]"/>
		<xsl:value-of select="$newline"/>

		<xsl:text>- </xsl:text>
		<xsl:value-of select="$presentation-linkbase/linkbase:linkbase/linkbase:presentationLink[2]/linkbase:loc[3]/@xlink:label"/>
		<xsl:value-of select="$newline"/>

	</xsl:template>



	<!-- recustive attempt
	<xsl:template name="retrieve-top-level-label-id">
		<xsl:param name="presentation-arcs"/>
		<xsl:param name="current-position"/>

		******
		<xsl:value-of select="$presentation-arcs[$current-position]/@xlink:from"/>
		<xsl:value-of select="$presentation-arcs[$current-position]/@xlink:to"/>
		******
		
		<xsl:variable name="to" select="$presentation-arcs[$current-position]/@xlink:to"/>
		
		<xsl:variable name="found">
			<xsl:choose>
				<xsl:when test="count($presentation-arcs[@xlink:from=$to]) &gt; 1">
					<xsl:text>yes</xsl:text>
				</xsl:when>
				<xsl:otherwise>
					<xsl:text>no</xsl:text>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>
		
		<xsl:text>found: </xsl:text>
		<xsl:value-of select="$found"/>
		<xsl:value-of select="$newline"/>

		
		
		<xsl:if test="not($current-position=count($presentation-arcs))">
			<xsl:call-template name="retrieve-top-level-label-id">
				<xsl:with-param name="presentation-arcs" select="$presentation-arcs"/>
				<xsl:with-param name="current-position" select="$current-position + 1"/>
			</xsl:call-template>
		</xsl:if>

	</xsl:template>
	
	<xsl:template name="retrieve-highest-presenatation-order">
		<xsl:param name="presentation-arcs"/>
		<xsl:for-each select="$presentation-arcs">
			<xsl:sort select="@order"/>
			<xsl:if test="position()=1">
				<xsl:value-of select="@order"/>
			</xsl:if>
		</xsl:for-each>
	</xsl:template>
 	-->
</xsl:stylesheet>
